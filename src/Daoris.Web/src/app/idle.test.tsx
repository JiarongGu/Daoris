import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// An idle window asks the shell for next to nothing, since a page that polls spends cores with nobody pressing anything: the
// whole window over a mocked shell and a fake clock, as `sessionsBadge.test.tsx` holds it, counting every route and service read
// over a minute. The page's own clocks are held here; a tick or a change is the shell's to send when something moved (`TickLinesTests`).

const { invoke, notifyReady, heard } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  heard: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => (await import('../test/doubles/appShell')).appShellMock(invoke, { notifyReady, holds: false, heard }));

import '../locales/i18n';
import { App } from './App';
import { AT_START } from '../help/setupGuide';
import { WorkspaceScopeProvider } from '../data/scope';

function respond(url: string): Response {
  if (url.startsWith('/api/registry')) {
    return Response.json([{ repository: 'engine', workspace: 'default', registered: true, adopted: true, owns: [], accepts: [], packs: [] }]);
  }
  if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical', note: '' });
  if (url.startsWith('/api/sync')) return Response.json({ workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] });
  return Response.json([]);
}

/** Every route and service read asked so far, by name, with how often. */
function asked(): Map<string, number> {
  const counts = new Map<string, number>();
  const count = (name: string) => counts.set(name, (counts.get(name) ?? 0) + 1);
  for (const [module, type] of invoke.mock.calls) count(`${String(module)}.${String(type)}`);
  for (const [url] of vi.mocked(fetch).mock.calls) count(String(url).split('?')[0]!);
  return counts;
}

/** What is asked between two counts, by name. */
const since = (before: Map<string, number>, after: Map<string, number>) => Object.fromEntries(
  [...after].map(([name, n]) => [name, n - (before.get(name) ?? 0)] as const).filter(([, n]) => n > 0),
);

/** The application's window over the mocked shell, settled past its first reads. */
async function open() {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (module: string, type: string) => {
    if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
    if (module === 'DAORIS.DRIVER' && type === 'STATE') {
      return { drivable: ['engine'], holds: [], trees: [], running: [], notify: false, strikes: 3, forgiven: {} };
    }
    return {};
  });
  window.localStorage.setItem(AT_START, 'off');
  // The application's own defaults (`main.tsx`): a read is fresh for 15 s.
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
}

const minute = async () => {
  const before = asked();
  await act(async () => { await vi.advanceTimersByTimeAsync(60_000); });
  return since(before, asked());
};

const hidden = (state: boolean) => {
  Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => (state ? 'hidden' : 'visible') });
  document.dispatchEvent(new Event('visibilitychange'));
};

describe('an idle window', () => {
  afterEach(() => {
    cleanup();
    hidden(false);
    vi.useRealTimers();
    vi.unstubAllGlobals();
    invoke.mockReset();
    heard.clear();
    window.localStorage.clear();
  });

  it("asks nothing in a minute but the update's slow look, which an event already keeps current", async () => {
    await open();
    expect(await minute()).toEqual({ 'DAORIS.UPDATE.STATE': 2 });
  });

  it('asks nothing at all while it is hidden or minimized', async () => {
    await open();
    hidden(true);
    expect(await minute()).toEqual({});
  });

  /**
   * What one forwarded tick costs: every list the window holds is asked again, the driver's git-backed ones among them (LINES and
   * SWEEP_PLAN ask git of every checkout). So the shell forwards a tick only when the look's report changed (`TickLinesTests`).
   */
  it('asks none of its pushed lists again at a tick, which the shell sends only when something moved', async () => {
    await open();
    const before = asked();
    await act(async () => {
      heard.get('DRIVER_TICK')?.({ events: [], considered: [], untrusted: [], waits: [] });
      await vi.advanceTimersByTimeAsync(2_000);
    });
    const tick = since(before, asked());
    for (const route of ['LINES', 'SWEEP_PLAN', 'SESSION_GROUPS']) expect(tick[`DAORIS.DRIVER.${route}`]).toBeUndefined();
  });
});
