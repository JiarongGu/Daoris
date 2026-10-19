import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The driver's state pushed with its data (PUSH1a, D182): the whole window over a mocked shell and a fake clock, counting every
// route read as IDLECPU1's `idle.test.tsx` does. A tick reads none of the pushed state again, a push changes it with no read, a
// focus asks nothing of it, a second window reads its one snapshot, and an older host keeps the pull.

const { invoke, notifyReady, heard } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  heard: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  return {
    ...actual,
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
    useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
    // The newest handler for each event: what the shell's pump would deliver to.
    useShenoraEvent: (_module: string, type: string, handler: (payload: unknown) => void) => { heard.set(type, handler); },
    useWindowMaximized: () => false,
  };
});

// The real hub, with a probe run in an effect declared before its own (so first among its effects), for PUSHMOUNT1's order.
const { mountProbe } = vi.hoisted(() => ({ mountProbe: { current: null as (() => void) | null } }));
vi.mock('./ShellSignals', async () => {
  const actual = await vi.importActual<typeof import('./ShellSignals')>('./ShellSignals');
  const React = await import('react');
  return {
    ...actual,
    ShellSignals: (props: React.ComponentProps<typeof actual.ShellSignals>) => {
      // A sibling ahead of the hub: its effect runs just before the hub's own, after every effect of what renders earlier.
      const Probe = () => { React.useEffect(() => { mountProbe.current?.(); }, []); return null; };
      return React.createElement(React.Fragment, null, React.createElement(Probe), React.createElement(actual.ShellSignals, props));
    },
  };
});

import '../locales/i18n';
import { App } from './App';
import { ShellSignals } from './ShellSignals';
import { AT_START } from '../help/setupGuide';
import { WorkspaceScopeProvider } from '../data/scope';
import { keys, useHelpSessions, useSessions, useSyncStanding } from '../data/queries';
import { type DriverState, useDriver } from '../bridge/driver';
import { useLines, useSweepLook, useSweepPlan, useWorkState } from '../bridge/repositories/lines';
import { useSessionEntries, useSessionGroups } from '../bridge/sessions/sessions';

const CHOICES = { drivable: ['engine'], holds: [], trees: [], notify: false, strikes: 3, forgiven: {} };

/** The driver's slice as the shell's owner holds it: three partitions, each at its first number. */
const SNAPSHOT = {
  slice: 'driver', epoch: 'e1', status: 'ready',
  partitions: [
    { partition: 'shell', seq: 1, status: 'ready', data: { ready: true } },
    { partition: 'choices', seq: 1, status: 'ready', data: CHOICES },
    { partition: 'running', seq: 1, status: 'ready', data: { running: [] } },
  ],
};

const lineRow = (repository: string, extra: Record<string, unknown> = {}) =>
  ({ repository, workspace: 'default', branch: 'main', source: 'checkout', ...extra });
const linesPart = (repository: string, extra: Record<string, unknown> = {}) => ({
  lines: [lineRow(repository, extra)], landings: [], languages: [], reviews: [], opinions: [], helpers: [], commits: [],
});

/** The lines slice as the shell's owner holds it: a partition per repository. */
const LINES_SNAPSHOT = {
  slice: 'lines', epoch: 'l1', status: 'ready',
  partitions: [
    { partition: 'engine', seq: 1, status: 'ready', data: linesPart('engine') },
    { partition: 'site', seq: 1, status: 'ready', data: linesPart('site', { branch: null, source: 'none', notReadYet: true }) },
  ],
};

const record = (id: string, extra: Record<string, unknown> = {}) => ({
  id, repository: 'engine', adapter: 'claude-code', state: 'completed', created: '2026-10-19T08:00:00+00:00',
  updated: '2026-10-19T09:00:00+00:00', workspace: 'default', ...extra,
});

/** The sessions' slice as the shell's owner holds it (PUSH1d2): a partition per workspace, its records by id. */
const SESSIONS_SNAPSHOT = {
  slice: 'sessions', epoch: 's1', status: 'ready',
  partitions: [
    { partition: 'default', seq: 1, status: 'ready', data: { 'a-1': record('a-1', { state: 'working' }), 'a-2': record('a-2') } },
    { partition: 'games', seq: 1, status: 'ready', data: { 'g-1': record('g-1', { repository: 'site', workspace: 'games' }) } },
  ],
};

const CALL = { title: 'npm test', since: '2026-10-20T08:00:00+00:00' };

/** The running calls' slice (WAITSAY1c2): one partition, session id to its call. */
const CALLS_SNAPSHOT = {
  slice: 'calls', epoch: 'c1', status: 'ready',
  partitions: [{ partition: 'calling', seq: 1, status: 'ready', data: { 'a-1': CALL } }],
};

const grouping = (session: string, group: string) =>
  ({ session, group, shown: group === 'working' ? 'working' : 'completed', archived: false, teammate: false });
const entry = (session: string, group: string) => ({
  entry: `session:${session}/engine`, origin: `session:${session}`, repository: 'engine', group, leading: session,
  parts: [{ session, quest: null, shown: grouping(session, group).shown, group, began: 'opened', after: null, askedBy: null }],
});

/** The session groups' slice (PUSH1c3a): each row under its session with its group's sort inputs, each entry under its key. */
const GROUPS_SNAPSHOT = {
  slice: 'groups', epoch: 'g1', status: 'ready',
  partitions: [
    { partition: 'entries', seq: 1, status: 'ready', data: { 'session:a-2/engine': entry('a-2', 'ended'), 'session:a-1/engine': entry('a-1', 'working') } },
    {
      partition: 'rows', seq: 1, status: 'ready',
      data: {
        'a-2': { ...grouping('a-2', 'ended'), updated: '2026-10-19T09:00:00+00:00' },
        'a-1': { ...grouping('a-1', 'working'), repository: 'engine', created: '2026-10-19T08:00:00+00:00' },
      },
    },
  ],
};

const unitRow = (unit: string, repository: string) =>
  ({ unit, repository, end: { state: 'unknown', said: '' }, result: [], cleanup: { state: 'pending', waits: [], gone: [] }, recovery: [], entries: [] });

/** The work slice as the shell's owner holds it (PUSH1c2b): a partition per repository, `<no checkout>`, and `<held>` apart. */
const WORK_SNAPSHOT = {
  slice: 'work', epoch: 'w1', status: 'ready',
  partitions: [
    { partition: '<held>', seq: 1, status: 'ready', data: { held: [] } },
    { partition: '<no checkout>', seq: 1, status: 'ready', data: { units: [], recovery: [], unread: [] } },
    { partition: 'engine', seq: 1, status: 'ready', data: { units: [unitRow('u-1', 'engine')], recovery: [], unread: [] } },
  ],
};

/** Where the one wired circle stands, as `/api/sync` answers it for `default`. */
const STANDING = {
  workspace: 'default', wired: true, ahead: 2, behind: ['q7'], conflicts: [], synced: '2026-10-20T08:00:00+00:00', tried: '2026-10-20T09:00:00+00:00',
};

/** The sync slice as the shell's owner holds it (PUSH1a2): a partition per wired circle, each the route's record for it. */
const SYNC_SNAPSHOT = {
  slice: 'sync', epoch: 'y1', status: 'ready',
  partitions: [{ partition: 'default', seq: 1, status: 'ready', data: STANDING }],
};

const sweepRow = (branch: string) =>
  ({ repository: 'engine', workspace: 'default', branch, hasTree: false, kind: 'empty', commits: 0, removable: true });

/** The clean-up's list as the shell's owner holds it (PUSH1c4a): a partition per repository with a checkout here. */
const SWEEP_SNAPSHOT = {
  slice: 'sweep', epoch: 'p1', status: 'ready',
  partitions: [{ partition: 'engine', seq: 1, status: 'ready', data: { branches: [sweepRow('daoris/s-one')], landed: [] } }],
};

/** How often the page read the sessions from the service: `/api/sessions` with any of its questions, never a record's own door. */
const sessionReads = () => vi.mocked(fetch).mock.calls.filter(([input]) => /^\/api\/sessions(\?|$)/.test(String(input))).length;

/** How often the page asked the service where a circle stands (`/api/sync`), whichever circle. */
const syncReads = () => vi.mocked(fetch).mock.calls.filter(([input]) => /^\/api\/sync(\?|$)/.test(String(input))).length;

function respond(url: string): Response {
  if (url.startsWith('/api/registry')) {
    return Response.json([{ repository: 'engine', workspace: 'default', registered: true, adopted: true, owns: [], accepts: [], packs: [] }]);
  }
  if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical', note: '' });
  if (url.startsWith('/api/sync')) return Response.json({ workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] });
  return Response.json([]);
}

/** Every route read so far, by name, with how often. */
function asked(): Map<string, number> {
  const counts = new Map<string, number>();
  for (const [module, type] of invoke.mock.calls) {
    const name = `${String(module)}.${String(type)}`;
    counts.set(name, (counts.get(name) ?? 0) + 1);
  }
  return counts;
}

const since = (before: Map<string, number>, after: Map<string, number>) => Object.fromEntries(
  [...after].map(([name, n]) => [name, n - (before.get(name) ?? 0)] as const).filter(([, n]) => n > 0),
);

/** A host that pushes the driver's state, or one older than the push that refuses its snapshot. */
function host(pushes: boolean) {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (module: string, type: string, options?: { payload?: { slice?: string } }) => {
    if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
    if (module === 'DAORIS.DRIVER' && type === 'STATE_SLICE') {
      if (pushes) {
        const slice = options?.payload?.slice;
        return slice === 'lines' ? LINES_SNAPSHOT : slice === 'sessions' ? SESSIONS_SNAPSHOT : slice === 'calls' ? CALLS_SNAPSHOT
          : slice === 'work' ? WORK_SNAPSHOT : slice === 'groups' ? GROUPS_SNAPSHOT : slice === 'sync' ? SYNC_SNAPSHOT
            : slice === 'sweep' ? SWEEP_SNAPSHOT : SNAPSHOT;
      }
      throw Object.assign(new Error('NO_ROUTE'), { code: 'NO_ROUTE' });
    }
    if (module === 'DAORIS.DRIVER' && type === 'LINES') return { lines: [lineRow('engine')], landings: [] };
    if (module === 'DAORIS.DRIVER' && type === 'SWEEP_PLAN') return { branches: [sweepRow('daoris/s-pulled')], landed: [] };
    if (module === 'DAORIS.DRIVER' && type === 'STATE') return { ...CHOICES, running: [] };
    return {};
  });
  window.localStorage.setItem(AT_START, 'off');
}

/** The application's window, settled past its first reads; its own query client, as each window has. */
async function open() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 15_000 } } });
  render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={null}>
          <App />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
  await act(async () => { await vi.advanceTimersByTimeAsync(10_000); });
  return client;
}

const later = async (ms: number) => { await act(async () => { await vi.advanceTimersByTimeAsync(ms); }); };

const hidden = (state: boolean) => {
  Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => (state ? 'hidden' : 'visible') });
  document.dispatchEvent(new Event('visibilitychange'));
  // Where the query cache hears a window come back into view.
  window.dispatchEvent(new Event('visibilitychange'));
};

describe("the driver's state, pushed (PUSH1a)", () => {
  afterEach(() => {
    cleanup();
    hidden(false);
    vi.useRealTimers();
    vi.unstubAllGlobals();
    invoke.mockReset();
    heard.clear();
    window.localStorage.clear();
  });

  it('is read once as its snapshot when the window opens, and a tick reads none of it again', async () => {
    host(true);
    const client = await open();
    // One snapshot for each slice the window reads pushed (the driver's state, the lines, the sessions, the calls, the work, the
    // groups, where each circle stands, the clean-up's list), and no pull beside any.
    expect(asked().get('DAORIS.DRIVER.STATE_SLICE')).toBe(8);
    expect(syncReads()).toBe(0);
    expect(asked().get('DAORIS.DRIVER.WORK_STATE')).toBeUndefined();
    expect(asked().get('DAORIS.DRIVER.SWEEP_PLAN')).toBeUndefined();
    expect(asked().get('DAORIS.DRIVER.SESSION_GROUPS')).toBeUndefined();
    expect(asked().get('DAORIS.DRIVER.STATE')).toBeUndefined();
    expect(asked().get('DAORIS.DRIVER.LINES')).toBeUndefined();
    expect(client.getQueryData<DriverState>(keys.driver)?.drivable).toEqual(['engine']);

    const before = asked();
    await act(async () => {
      heard.get('DRIVER_TICK')?.({ events: [], considered: [], untrusted: [], waits: [] });
      await vi.advanceTimersByTimeAsync(2_000);
    });
    const tick = since(before, asked());

    expect(tick['DAORIS.DRIVER.STATE']).toBeUndefined();
    expect(tick['DAORIS.DRIVER.STATE_SLICE']).toBeUndefined();
    expect(tick['DAORIS.DRIVER.LINES']).toBeUndefined();
    // Nor the clean-up's list, whose every read asked the pull-request plugin (PUSH1c4b).
    expect(tick['DAORIS.DRIVER.SWEEP_PLAN']).toBeUndefined();
  });

  it('opens on the Sessions view with one snapshot of each slice and neither pull (PUSHMOUNT1)', async () => {
    host(true);
    // The view that mounts the driver's state and the lines first, remembered as the opening one.
    window.localStorage.setItem('daoris.view', 'sessions');
    await open();
    expect(asked().get('DAORIS.DRIVER.STATE_SLICE')).toBe(8);
    expect(asked().get('DAORIS.DRIVER.STATE')).toBeUndefined();
    expect(asked().get('DAORIS.DRIVER.LINES')).toBeUndefined();
    expect(asked().get('DAORIS.DRIVER.SESSION_GROUPS')).toBeUndefined();
    expect(sessionReads()).toBe(0);
  });

  it('mounts the hub before any view asks the shell anything (PUSHMOUNT1)', async () => {
    host(true);
    window.localStorage.setItem('daoris.view', 'sessions');
    // The hub's effects run first when it is the first thing the root renders: nothing below it has asked the bridge by then.
    const callsAtMount: number[] = [];
    mountProbe.current = () => callsAtMount.push(invoke.mock.calls.length);
    await open();
    mountProbe.current = null;
    expect(callsAtMount[0]).toBe(0);
  });

  it('puts the lines in the cache composed from the repositories, with no pull', async () => {
    host(true);
    const client = await open();
    const lines = client.getQueryData<{ lines: { repository: string; notReadYet?: boolean }[] }>(keys.lines);
    expect(lines?.lines.map((line) => line.repository)).toEqual(['engine', 'site']);
    expect(lines?.lines[1].notReadYet).toBe(true);
  });

  it('changes the lines where a push says, with no read, and drops a repository pushed gone', async () => {
    host(true);
    const client = await open();
    const before = asked();

    await act(async () => {
      heard.get('STATE_PUSH')?.({ slice: 'lines', epoch: 'l1', partition: 'engine', seq: 2, status: 'ready', data: linesPart('engine', { branch: 'dev' }) });
      heard.get('STATE_PUSH')?.({ slice: 'lines', epoch: 'l1', partition: 'site', seq: 2, status: 'gone', data: null });
      heard.get('STATE_CHANGED')?.({ scopes: ['lines'] });
      await vi.advanceTimersByTimeAsync(2_000);
    });

    const lines = client.getQueryData<{ lines: { repository: string; branch: string }[] }>(keys.lines);
    expect(lines?.lines).toMatchObject([{ repository: 'engine', branch: 'dev' }]);
    const changed = since(before, asked());
    expect(changed['DAORIS.DRIVER.LINES']).toBeUndefined();
    expect(changed['DAORIS.DRIVER.STATE_SLICE']).toBeUndefined();
  });

  it('asks nothing of the lines when the window takes the focus again', async () => {
    host(true);
    await open();
    await later(20_000);
    const before = asked();

    hidden(true);
    hidden(false);
    await later(1_000);

    expect(since(before, asked())['DAORIS.DRIVER.LINES']).toBeUndefined();
  });

  it('changes where a push says, with no read, and a change the driver declared reads none of it either', async () => {
    host(true);
    const client = await open();
    const before = asked();

    await act(async () => {
      heard.get('STATE_PUSH')?.({ slice: 'driver', epoch: 'e1', partition: 'choices', seq: 2, status: 'ready', data: { ...CHOICES, notify: true } });
      heard.get('STATE_CHANGED')?.({ scopes: ['driver'] });
      await vi.advanceTimersByTimeAsync(2_000);
    });

    expect(client.getQueryData<DriverState>(keys.driver)?.notify).toBe(true);
    const changed = since(before, asked());
    expect(changed['DAORIS.DRIVER.STATE']).toBeUndefined();
    expect(changed['DAORIS.DRIVER.STATE_SLICE']).toBeUndefined();
  });

  it('asks nothing of it when the window takes the focus again', async () => {
    host(true);
    await open();
    await later(20_000);
    const before = asked();

    hidden(true);
    hidden(false);
    await later(1_000);

    expect(since(before, asked())['DAORIS.DRIVER.STATE']).toBeUndefined();
  });

  it('reads one snapshot in a second window, and nothing judged again', async () => {
    host(true);
    await open();
    const before = asked();

    const reads = sessionReads();
    const second = new QueryClient();
    function Sessions() {
      useSessions(null, true);
      useHelpSessions(1_500);
      return null;
    }
    function Second() {
      useDriver();
      useLines();
      // The hub first, as each root mounts it (PUSHMOUNT1), then what reads the sessions.
      return <><ShellSignals notify={() => {}} /><Sessions /></>;
    }
    render(<QueryClientProvider client={second}><WorkspaceScopeProvider initial={null}><Second /></WorkspaceScopeProvider></QueryClientProvider>);
    await later(2_000);

    // One snapshot for each slice, and no pull. The first window's own ask of the held permissions, every few seconds while a
    // session works (AUTONOMY1b3c, `permissionsAgainIn`), is not this window's, and may land in these two seconds.
    const { 'DAORIS.DRIVER.PERMISSION_WAITS': _firstWindows, ...read } = since(before, asked());
    expect(read).toEqual({ 'DAORIS.DRIVER.STATE_SLICE': 8, 'DAORIS.DRIVER.UNDER_WAY': 1 });
    expect(sessionReads()).toBe(reads);
    expect(second.getQueryData<{ id: string }[]>(keys.sessions(null, true, null))?.map((each) => each.id)).toEqual(['a-1', 'a-2', 'g-1']);
  });

  it('reads the sessions as their snapshot, and neither a tick, a focus nor an ending reads /api/sessions (PUSH1d2)', async () => {
    host(true);
    const client = await open();
    expect(sessionReads()).toBe(0);
    // The widest list, every workspace's, closed included, in the service's order: the open first.
    expect(client.getQueryData<{ id: string }[]>(keys.sessions(null, true, null))?.map((each) => each.id)).toEqual(['a-1', 'a-2', 'g-1']);

    await act(async () => {
      heard.get('DRIVER_TICK')?.({ events: [], considered: [], untrusted: [], waits: [] });
      heard.get('SESSION_ENDED')?.({ session: 'a-1', state: 'completed' });
      heard.get('SESSION_ATTENTION')?.({ headline: 'It ended', kind: 'Ended', session: 'a-1' });
      heard.get('STATE_CHANGED')?.({ scopes: ['sessions'], sessions: ['a-1'] });
      await vi.advanceTimersByTimeAsync(2_000);
    });
    await later(20_000);
    hidden(true);
    hidden(false);
    await later(1_000);

    expect(sessionReads()).toBe(0);
  });

  it("changes the sessions where a push says, a record's delta alone, with no read, and each list read from them (PUSH1d2)", async () => {
    host(true);
    const client = await open();
    const before = asked();
    let running: { id: string }[] | undefined;
    let engine: { id: string }[] | undefined;
    function Lists() {
      running = useSessions(null, false).data;
      engine = useSessions('engine', true).data;
      return null;
    }
    render(<QueryClientProvider client={client}><WorkspaceScopeProvider initial={null}><Lists /></WorkspaceScopeProvider></QueryClientProvider>);
    await later(1_000);
    expect(running?.map((each) => each.id)).toEqual(['a-1']);
    expect(engine?.map((each) => each.id)).toEqual(['a-1', 'a-2']);

    await act(async () => {
      heard.get('STATE_PUSH')?.({
        slice: 'sessions', epoch: 's1', partition: 'default', seq: 2, status: 'ready', baseSeq: 1, removed: ['a-2'],
        data: { 'a-1': record('a-1', { state: 'awaiting-person' }), 'a-3': record('a-3', { state: 'queued', created: '2026-10-19T10:00:00+00:00' }) },
      });
      await vi.advanceTimersByTimeAsync(1_000);
    });

    expect(running?.map((each) => each.id)).toEqual(['a-1', 'a-3']);
    expect(engine?.map((each) => each.id)).toEqual(['a-1', 'a-3']);
    expect(sessionReads()).toBe(0);
    expect(since(before, asked())['DAORIS.DRIVER.STATE_SLICE']).toBeUndefined();
  });

  it('holds the running calls from their snapshot, a delta or a whole push or an empty one moving them, a tick or a focus reading none (WAITSAY1c2)', async () => {
    host(true);
    const client = await open();
    expect(client.getQueryData(keys.calls)).toEqual({ 'a-1': CALL });
    const before = asked();
    const other = { title: 'dotnet build', since: '2026-10-20T08:05:00+00:00' };

    await act(async () => {
      heard.get('DRIVER_TICK')?.({ events: [], considered: [], untrusted: [], waits: [] });
      heard.get('STATE_PUSH')?.({ slice: 'calls', epoch: 'c1', partition: 'calling', seq: 2, status: 'ready', baseSeq: 1, removed: [], data: { 'a-2': other } });
      await vi.advanceTimersByTimeAsync(1_000);
    });
    expect(client.getQueryData(keys.calls)).toEqual({ 'a-1': CALL, 'a-2': other });

    // Half the keys moved: the owner pushes the partition whole, and a judged-empty partition is a whole push of {}.
    await act(async () => {
      heard.get('STATE_PUSH')?.({ slice: 'calls', epoch: 'c1', partition: 'calling', seq: 3, status: 'ready', data: { 'a-2': other } });
      await vi.advanceTimersByTimeAsync(1_000);
    });
    expect(client.getQueryData(keys.calls)).toEqual({ 'a-2': other });
    await act(async () => {
      heard.get('STATE_PUSH')?.({ slice: 'calls', epoch: 'c1', partition: 'calling', seq: 4, status: 'ready', data: {} });
      await vi.advanceTimersByTimeAsync(1_000);
    });
    expect(client.getQueryData(keys.calls)).toEqual({});

    hidden(true);
    hidden(false);
    await later(1_000);
    expect(since(before, asked())['DAORIS.DRIVER.STATE_SLICE']).toBeUndefined();
  });

  /**
   * AUTONOMY1b3b: where the calls are pushed, the held permissions are asked as a pushed call's waiting mark moves, and never every
   * few seconds while a session works: the mark comes with the hold and goes with its answer or release.
   */
  it('asks the held permissions as a pushed call is marked waiting and unmarked, and never on a clock (AUTONOMY1b3b)', async () => {
    host(true);
    await open();
    const before = asked();
    await later(20_000);
    expect(since(before, asked())['DAORIS.DRIVER.PERMISSION_WAITS']).toBeUndefined();

    const held = asked();
    await act(async () => {
      heard.get('STATE_PUSH')?.({
        slice: 'calls', epoch: 'c1', partition: 'calling', seq: 2, status: 'ready', baseSeq: 1, removed: [],
        data: { 'a-1': { title: 'git push', since: '2026-10-20T08:03:00+00:00', waiting: true } },
      });
      await vi.advanceTimersByTimeAsync(1_000);
    });
    expect(since(held, asked())['DAORIS.DRIVER.PERMISSION_WAITS']).toBe(1);

    const answered = asked();
    await act(async () => {
      heard.get('STATE_PUSH')?.({
        slice: 'calls', epoch: 'c1', partition: 'calling', seq: 3, status: 'ready', baseSeq: 2, removed: [], data: { 'a-1': CALL },
      });
      await vi.advanceTimersByTimeAsync(1_000);
    });
    expect(since(answered, asked())['DAORIS.DRIVER.PERMISSION_WAITS']).toBe(1);
    await later(20_000);
    expect(since(answered, asked())['DAORIS.DRIVER.PERMISSION_WAITS']).toBe(1);
  });

  /** A host older than the calls' push has no mark to wake the page: it keeps asking every few seconds while a session works. */
  it('keeps asking the held permissions every few seconds on a host that pushes no calls', async () => {
    host(false);
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => (/^\/api\/sessions(\?|$)/.test(String(input))
      ? Response.json([record('a-1', { state: 'working' })]) : respond(String(input)))));
    await open();
    const before = asked();
    await later(20_000);
    expect(since(before, asked())['DAORIS.DRIVER.PERMISSION_WAITS']).toBeGreaterThanOrEqual(4);
  });

  it('holds the work milestones from their snapshot, a push changing them and a tick or a focus reading no WORK_STATE (PUSH1c2b)', async () => {
    host(true);
    const client = await open();
    expect(asked().get('DAORIS.DRIVER.WORK_STATE')).toBeUndefined();
    expect(client.getQueryData<{ units: { unit: string }[] }>(keys.workState(null))?.units.map((each) => each.unit)).toEqual(['u-1']);
    const before = asked();
    let unit: { units: { unit: string }[]; recovery: unknown[] } | undefined;
    function One() {
      unit = useWorkState('U-2').data;
      return null;
    }
    render(<QueryClientProvider client={client}><WorkspaceScopeProvider initial={null}><One /></WorkspaceScopeProvider></QueryClientProvider>);

    await act(async () => {
      heard.get('DRIVER_TICK')?.({ events: [], considered: [], untrusted: [], waits: [] });
      heard.get('SESSION_ENDED')?.({ session: 'a-1', state: 'completed' });
      heard.get('STATE_CHANGED')?.({ scopes: ['sessions', 'landing', 'sweep'] });
      await vi.advanceTimersByTimeAsync(2_000);
    });
    hidden(true);
    hidden(false);
    await later(20_000);
    expect(since(before, asked())['DAORIS.DRIVER.WORK_STATE']).toBeUndefined();
    expect(unit?.units).toEqual([]);

    await act(async () => {
      heard.get('STATE_PUSH')?.({
        slice: 'work', epoch: 'w1', partition: 'engine', seq: 2, status: 'ready',
        data: { units: [unitRow('u-1', 'engine'), unitRow('u-2', 'engine')], recovery: [], unread: [] },
      });
      await vi.advanceTimersByTimeAsync(1_000);
    });
    expect(client.getQueryData<{ units: unknown[] }>(keys.workState(null))?.units).toHaveLength(2);
    expect(unit?.units.map((each) => each.unit)).toEqual(['u-2']);
    expect(unit?.recovery).toEqual([]);
    expect(since(before, asked())['DAORIS.DRIVER.WORK_STATE']).toBeUndefined();
  });

  it('holds the session groups from their snapshot in the reader\'s order, and a tick, a focus or a change reads no SESSION_GROUPS (PUSH1c3b)', async () => {
    host(true);
    const client = await open();
    expect(asked().get('DAORIS.DRIVER.SESSION_GROUPS')).toBeUndefined();
    // Working before ended, the order's inputs left out, each entry at its leading row's place.
    const held = client.getQueryData<{ sessions: { session: string }[]; entries: { leading: string }[] }>(['sessions', 'groups', '*']);
    expect(held?.sessions).toEqual([grouping('a-1', 'working'), grouping('a-2', 'ended')]);
    expect(held?.entries.map((each) => each.leading)).toEqual(['a-1', 'a-2']);
    const before = asked();

    await act(async () => {
      heard.get('DRIVER_TICK')?.({ events: [], considered: [], untrusted: [], waits: [] });
      heard.get('SESSION_ENDED')?.({ session: 'a-1', state: 'completed' });
      heard.get('SESSION_ATTENTION')?.({ headline: 'It ended', kind: 'Ended', session: 'a-1' });
      heard.get('STATE_CHANGED')?.({ scopes: ['sessions'], sessions: ['a-1'] });
      await vi.advanceTimersByTimeAsync(2_000);
    });
    await later(20_000);
    hidden(true);
    hidden(false);
    await later(1_000);

    expect(since(before, asked())['DAORIS.DRIVER.SESSION_GROUPS']).toBeUndefined();
    expect(since(before, asked())['DAORIS.DRIVER.STATE_SLICE']).toBeUndefined();
  });

  it("changes the rail's groups where a push says, a session's delta alone, with no read, and some sessions' variant read from them (PUSH1c3b)", async () => {
    host(true);
    const client = await open();
    const before = asked();
    let rows: { session: string }[] | undefined;
    let entries: { leading: string }[] | undefined;
    let some: { session: string }[] | undefined;
    function Rail() {
      rows = useSessionGroups().data;
      entries = useSessionEntries().data;
      some = useSessionGroups(['a-2']).data;
      return null;
    }
    render(<QueryClientProvider client={client}><WorkspaceScopeProvider initial={null}><Rail /></WorkspaceScopeProvider></QueryClientProvider>);
    await later(1_000);
    expect(rows?.map((each) => each.session)).toEqual(['a-1', 'a-2']);
    expect(entries?.map((each) => each.leading)).toEqual(['a-1', 'a-2']);
    expect(some?.map((each) => each.session)).toEqual(['a-2']);

    // A session began in another repository and one went: its entry and its row, each a delta, the entries first as the owner pushes.
    await act(async () => {
      heard.get('STATE_PUSH')?.({
        slice: 'groups', epoch: 'g1', partition: 'entries', seq: 2, status: 'ready', baseSeq: 1, removed: ['session:a-2/engine'],
        data: { 'session:a-3/engine': entry('a-3', 'working') },
      });
      heard.get('STATE_PUSH')?.({
        slice: 'groups', epoch: 'g1', partition: 'rows', seq: 2, status: 'ready', baseSeq: 1, removed: ['a-2'],
        data: { 'a-3': { ...grouping('a-3', 'working'), repository: 'Alpha', created: '2026-10-19T10:00:00+00:00' } },
      });
      await vi.advanceTimersByTimeAsync(1_000);
    });

    // Working by repository without case: 'Alpha' before 'engine', though it began later.
    expect(rows?.map((each) => each.session)).toEqual(['a-3', 'a-1']);
    expect(entries?.map((each) => each.leading)).toEqual(['a-3', 'a-1']);
    expect(some).toEqual([]);
    expect(since(before, asked())['DAORIS.DRIVER.SESSION_GROUPS']).toBeUndefined();
    expect(since(before, asked())['DAORIS.DRIVER.STATE_SLICE']).toBeUndefined();
  });

  it("holds the clean-up's list from its snapshot, a push moving it, a tick, a focus or a change reading no SWEEP_PLAN, and a look asking it once (PUSH1c4b)", async () => {
    host(true);
    const client = await open();
    expect(asked().get('DAORIS.DRIVER.SWEEP_PLAN')).toBeUndefined();
    const before = asked();
    let rows: { branch: string }[] | undefined;
    let look: ReturnType<typeof useSweepLook> | undefined;
    function Branches() {
      rows = useSweepPlan().data?.branches;
      look = useSweepLook();
      return null;
    }
    render(<QueryClientProvider client={client}><WorkspaceScopeProvider initial={null}><Branches /></WorkspaceScopeProvider></QueryClientProvider>);
    await later(1_000);
    expect(rows?.map((each) => each.branch)).toEqual(['daoris/s-one']);

    await act(async () => {
      heard.get('DRIVER_TICK')?.({ events: [], considered: [], untrusted: [], waits: [] });
      heard.get('STATE_CHANGED')?.({ scopes: ['sessions', 'landing', 'sweep'] });
      await vi.advanceTimersByTimeAsync(2_000);
    });
    // Past the minute the pull was fresh for, then a focus.
    await later(61_000);
    hidden(true);
    hidden(false);
    await later(1_000);
    expect(since(before, asked())['DAORIS.DRIVER.SWEEP_PLAN']).toBeUndefined();

    // A repository's part pushed whole: its rows move, with no read.
    await act(async () => {
      heard.get('STATE_PUSH')?.({
        slice: 'sweep', epoch: 'p1', partition: 'engine', seq: 2, status: 'ready', data: { branches: [sweepRow('daoris/s-two')], landed: [] },
      });
      await vi.advanceTimersByTimeAsync(1_000);
    });
    expect(rows?.map((each) => each.branch)).toEqual(['daoris/s-two']);

    // The person's look asks it once, the occasion of its pull-request asks; what it heard comes as a push, never as its answer.
    await act(async () => {
      look?.mutate();
      await vi.advanceTimersByTimeAsync(2_000);
    });
    expect(since(before, asked())['DAORIS.DRIVER.SWEEP_PLAN']).toBe(1);
    expect(rows?.map((each) => each.branch)).toEqual(['daoris/s-two']);
    expect(since(before, asked())['DAORIS.DRIVER.STATE_SLICE']).toBeUndefined();
  });

  it('holds where each circle stands from its snapshot, and a tick, a pass\'s scope or a focus reads no /api/sync (PUSH1a2)', async () => {
    host(true);
    const client = await open();
    let standing: unknown;
    let unwired: unknown;
    let spelled: unknown;
    function Standings() {
      standing = useSyncStanding('default').data;
      unwired = useSyncStanding('games').data;
      spelled = useSyncStanding(' Default ').data;
      return null;
    }
    render(<QueryClientProvider client={client}><WorkspaceScopeProvider initial={null}><Standings /></WorkspaceScopeProvider></QueryClientProvider>);
    await later(1_000);

    // The route's own answer for each: the wired circle's record, a circle with no remote here `wired: false` and nothing else, and
    // a circle asked as a person spells it named as asked, trimmed, the service comparing it without case.
    expect(standing).toEqual(STANDING);
    expect(unwired).toEqual({ workspace: 'games', wired: false, ahead: 0, behind: [], conflicts: [] });
    expect(spelled).toEqual({ ...STANDING, workspace: 'Default' });
    const before = asked();

    await act(async () => {
      heard.get('DRIVER_TICK')?.({ events: [], considered: [], untrusted: [], waits: [] });
      // *Sync now*'s declared change, as `SYNC_NOW` publishes it.
      heard.get('STATE_CHANGED')?.({ scopes: ['sessions', 'quests', 'sync', 'registry'] });
      await vi.advanceTimersByTimeAsync(2_000);
    });
    await later(20_000);
    hidden(true);
    hidden(false);
    await later(1_000);

    expect(syncReads()).toBe(0);
    expect(since(before, asked())['DAORIS.DRIVER.STATE_SLICE']).toBeUndefined();
  });

  it('moves where a circle stands in two windows from the push a pass made, with no read in either (PUSH1a2)', async () => {
    host(true);
    const client = await open();
    const firstWindow = heard.get('STATE_PUSH')!;
    let first: { ahead: number; tried?: string | null } | undefined;
    function First() {
      first = useSyncStanding('default').data;
      return null;
    }
    render(<QueryClientProvider client={client}><WorkspaceScopeProvider initial={null}><First /></WorkspaceScopeProvider></QueryClientProvider>);
    await later(1_000);
    const before = asked();

    const second = new QueryClient();
    let other: { ahead: number; tried?: string | null } | undefined;
    function Second() {
      other = useSyncStanding('default').data;
      return null;
    }
    // The hub first, as each root mounts it (PUSHMOUNT1), then what reads where the circle stands.
    render(
      <QueryClientProvider client={second}>
        <WorkspaceScopeProvider initial={null}><ShellSignals notify={() => {}} /><Second /></WorkspaceScopeProvider>
      </QueryClientProvider>,
    );
    await later(2_000);
    const secondWindow = heard.get('STATE_PUSH')!;
    expect(other).toMatchObject({ ahead: 2 });

    // A pass that brought the circle level: the shell pushes the circle's record to each window.
    const pushed = { slice: 'sync', epoch: 'y1', partition: 'default', seq: 2, status: 'ready', data: { ...STANDING, ahead: 0, tried: '2026-10-20T09:05:00+00:00' } };
    await act(async () => {
      firstWindow(pushed);
      secondWindow(pushed);
      await vi.advanceTimersByTimeAsync(1_000);
    });

    expect(first).toMatchObject({ ahead: 0, tried: '2026-10-20T09:05:00+00:00' });
    expect(other).toMatchObject({ ahead: 0, tried: '2026-10-20T09:05:00+00:00' });
    expect(syncReads()).toBe(0);
    // The second window's own snapshots alone; the first window's ask of the held permissions is not this window's (AUTONOMY1b3c).
    const { 'DAORIS.DRIVER.PERMISSION_WAITS': _firstWindows, ...read } = since(before, asked());
    expect(read).toEqual({ 'DAORIS.DRIVER.STATE_SLICE': 8, 'DAORIS.DRIVER.UNDER_WAY': 1 });
  });

  it('keeps where a circle stands pulled on a host older than the push: read as the window opens, and read again at a tick (PUSH1a2)', async () => {
    host(false);
    await open();
    const opened = syncReads();
    expect(opened).toBeGreaterThan(0);

    await act(async () => {
      heard.get('DRIVER_TICK')?.({ events: [], considered: [], untrusted: [], waits: [] });
      await vi.advanceTimersByTimeAsync(2_000);
    });

    expect(syncReads()).toBeGreaterThan(opened);
  });

  it('keeps the groups pulled on a host older than the push: read as the window opens, and read again at a tick (PUSH1c3b)', async () => {
    host(false);
    await open();
    const opened = asked().get('DAORIS.DRIVER.SESSION_GROUPS') ?? 0;
    expect(opened).toBeGreaterThan(0);

    await act(async () => {
      heard.get('DRIVER_TICK')?.({ events: [], considered: [], untrusted: [], waits: [] });
      await vi.advanceTimersByTimeAsync(2_000);
    });

    expect(asked().get('DAORIS.DRIVER.SESSION_GROUPS')).toBeGreaterThan(opened);
  });

  it('keeps the pull on a host older than the push: its state is read, and read again at a tick', async () => {
    host(false);
    const client = await open();
    expect(asked().get('DAORIS.DRIVER.STATE')).toBe(1);
    expect(client.getQueryData<DriverState>(keys.driver)?.drivable).toEqual(['engine']);

    const before = asked();
    await act(async () => {
      heard.get('DRIVER_TICK')?.({ events: [], considered: [], untrusted: [], waits: [] });
      await vi.advanceTimersByTimeAsync(2_000);
    });

    expect(since(before, asked())['DAORIS.DRIVER.STATE']).toBe(1);
    // And the lines with it: an older host's page pulls them at the tick, as before.
    expect(since(before, asked())['DAORIS.DRIVER.LINES']).toBe(1);
    expect(asked().get('DAORIS.DRIVER.WORK_STATE')).toBeGreaterThan(0);
    // And the clean-up's list (PUSH1c4b).
    expect(since(before, asked())['DAORIS.DRIVER.SWEEP_PLAN']).toBe(1);
  });

  it('keeps the sessions pulled on a host older than the push: each list read from the service, and read again at a tick (PUSH1d2)', async () => {
    host(false);
    await open();
    const opened = sessionReads();
    expect(opened).toBeGreaterThan(0);

    await act(async () => {
      heard.get('DRIVER_TICK')?.({ events: [], considered: [], untrusted: [], waits: [] });
      await vi.advanceTimersByTimeAsync(2_000);
    });

    expect(sessionReads()).toBeGreaterThan(opened);
  });
});
