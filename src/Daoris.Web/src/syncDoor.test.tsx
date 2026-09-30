import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// NAME1b (UX5 U72): a menu item named for a part opens at that part. The sync menu's *Wiring…* is named
// for the Workspace domain's Wiring, so it opens there, as the Workspace menu's *Wire to a remote…* does:
// the whole window over a mocked shell, since the door is the application's.

const { invoke, notifyReady } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
}));

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  return {
    ...actual,
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
    useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
    useShenoraEvent: () => {},
    useWindowMaximized: () => false,
  };
});

import './i18n';
import { App } from './App';
import { AT_START } from './setupGuide';
import { WorkspaceScopeProvider } from './scope';

function respond(url: string): Response {
  if (url.startsWith('/api/registry')) {
    return Response.json([{ repository: 'engine', workspace: 'aurora', registered: true, adopted: true, owns: [], accepts: [], packs: [] }]);
  }
  if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical', note: '' });
  if (url.startsWith('/api/sync')) {
    return Response.json({
      workspace: 'aurora', wired: true, ahead: 0, behind: [], conflicts: [],
      synced: '2026-10-01T10:00:00Z', tried: '2026-10-01T10:00:00Z', problem: null,
    });
  }
  return Response.json([]);
}

function machine() {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (module: string, type: string) => {
    if (module === 'DAORIS.REMOTES') {
      return { path: 'remotes.json', fromEnvironment: false, remotes: [{ workspace: 'aurora', url: 'https://aurora.example.com', key: 'dk_abcd1234…' }] };
    }
    if (module === 'DAORIS.WINDOWS') return { opened: true, windows: [] };
    if (module === 'DAORIS.DRIVER' && type === 'STATE') {
      return { drivable: [], holds: [], trees: [], running: [], notify: false, strikes: 3, forgiven: {} };
    }
    return {};
  });
}

function start() {
  window.localStorage.setItem(AT_START, 'off');
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial="aurora">
          <App />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe("the sync menu's doors", () => {
  const original = Element.prototype.scrollIntoView;
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
    Element.prototype.scrollIntoView = original;
  });

  it('opens Wiring… at the Workspace domain’s Wiring, not at the top of the domain', async () => {
    const scrolled = vi.fn();
    Element.prototype.scrollIntoView = function scroll(this: Element) { scrolled(this.id); };
    machine();
    start();
    const user = userEvent.setup();

    const item = await screen.findByRole('button', { name: 'sync' });
    item.focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Wiring…' }));

    const domains = await screen.findByRole('navigation', { name: 'Settings domains' });
    expect(within(domains).getByRole('button', { name: 'Workspace' })).toHaveAttribute('aria-current', 'page');
    await waitFor(() => expect(scrolled).toHaveBeenCalledWith('settings-wiring'));
  });
});
