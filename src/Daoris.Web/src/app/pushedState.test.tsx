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

import '../locales/i18n';
import { App } from './App';
import { ShellSignals } from './ShellSignals';
import { AT_START } from '../help/setupGuide';
import { WorkspaceScopeProvider } from '../data/scope';
import { keys } from '../data/queries';
import { type DriverState, useDriver } from '../bridge/driver';

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
  invoke.mockImplementation(async (module: string, type: string) => {
    if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
    if (module === 'DAORIS.DRIVER' && type === 'STATE_SLICE') {
      if (pushes) return SNAPSHOT;
      throw Object.assign(new Error('NO_ROUTE'), { code: 'NO_ROUTE' });
    }
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
    expect(asked().get('DAORIS.DRIVER.STATE_SLICE')).toBe(1);
    expect(asked().get('DAORIS.DRIVER.STATE')).toBeUndefined();
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
    expect(tick).toMatchObject({ 'DAORIS.DRIVER.LINES': 1, 'DAORIS.DRIVER.SWEEP_PLAN': 1 });
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
      return <ShellSignals notify={() => {}} />;
    }
    render(<QueryClientProvider client={new QueryClient()}><Second /></QueryClientProvider>);
    await later(2_000);

    expect(since(before, asked())).toEqual({ 'DAORIS.DRIVER.STATE_SLICE': 1, 'DAORIS.DRIVER.UNDER_WAY': 1 });
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
  });
});
