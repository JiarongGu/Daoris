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
import { keys } from '../data/queries';
import { type DriverState, useDriver } from '../bridge/driver';
import { useLines } from '../bridge/lines';

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
      if (pushes) return options?.payload?.slice === 'lines' ? LINES_SNAPSHOT : SNAPSHOT;
      throw Object.assign(new Error('NO_ROUTE'), { code: 'NO_ROUTE' });
    }
    if (module === 'DAORIS.DRIVER' && type === 'LINES') return { lines: [lineRow('engine')], landings: [] };
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
    // One snapshot for each slice the window reads pushed (the driver's state, the lines), and no pull beside either.
    expect(asked().get('DAORIS.DRIVER.STATE_SLICE')).toBe(2);
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
    // Every other family keeps its pulls until its own slice moves.
    expect(tick['DAORIS.DRIVER.LINES']).toBeUndefined();
    expect(tick).toMatchObject({ 'DAORIS.DRIVER.SWEEP_PLAN': 1 });
  });

  it('opens on the Sessions view with one snapshot of each slice and neither pull (PUSHMOUNT1)', async () => {
    host(true);
    // The view that mounts the driver's state and the lines first, remembered as the opening one.
    window.localStorage.setItem('daoris.view', 'sessions');
    await open();
    expect(asked().get('DAORIS.DRIVER.STATE_SLICE')).toBe(2);
    expect(asked().get('DAORIS.DRIVER.STATE')).toBeUndefined();
    expect(asked().get('DAORIS.DRIVER.LINES')).toBeUndefined();
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

    function Second() {
      useDriver();
      useLines();
      return <ShellSignals notify={() => {}} />;
    }
    render(<QueryClientProvider client={new QueryClient()}><Second /></QueryClientProvider>);
    await later(2_000);

    // One snapshot for each slice, and neither pull.
    expect(since(before, asked())).toEqual({ 'DAORIS.DRIVER.STATE_SLICE': 2, 'DAORIS.DRIVER.UNDER_WAY': 1 });
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
  });
});
