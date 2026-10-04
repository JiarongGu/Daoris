import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// NAME1b (UX5 U72): a menu item named for a part opens at that part. The sync menu's *Remote and reach…* is named for the
// workspace's page's section where its remote is set (UX6g, D150 §4.3), so it opens there: the whole window over a mocked
// shell, since the door is the application's.

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
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it("opens Remote and reach… on the workspace's page, at its Setup's remote, open", async () => {
    machine();
    start();
    const user = userEvent.setup();

    const item = await screen.findByRole('button', { name: 'sync' });
    item.focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Remote and reach…' }));

    const main = await screen.findByRole('main');
    expect(await within(main).findByRole('heading', { level: 1, name: 'aurora' })).toBeInTheDocument();
    expect(within(main).getByRole('tab', { name: 'Setup' })).toHaveAttribute('aria-selected', 'true');
    await waitFor(() => expect(within(main).getByRole('button', { name: 'Remote and reach' })).toHaveAttribute('aria-expanded', 'true'));
    expect(within(main).getByText('https://aurora.example.com')).toBeInTheDocument();
  });

  /** D150 §2.4: the Workspace menu's *This workspace's setup* opens the workspace in view's page at Setup (UX6g). */
  it("opens This workspace's setup on the workspace in view's page, at Setup", async () => {
    machine();
    start();
    const user = userEvent.setup();

    (await screen.findByRole('button', { name: 'Workspace' })).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: "This workspace's setup" }));

    const main = await screen.findByRole('main');
    expect(await within(main).findByRole('heading', { level: 1, name: 'aurora' })).toBeInTheDocument();
    expect(within(main).getByRole('tab', { name: 'Setup' })).toHaveAttribute('aria-selected', 'true');
    expect(within(main).getByRole('button', { name: 'Defaults' })).toBeInTheDocument();
  });
});
