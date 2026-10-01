import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// FRAME1g on the window (D118 §3a, §3f): Settings' list is a list pane the application holds, so its four
// doors — the strip's toggle, the View menu's item, Ctrl+B and a press on the Settings place — are Settings'
// too, named for it, and its domain is remembered under the key it always was.

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
    return Response.json([{ repository: 'engine', workspace: 'default', registered: true, adopted: true, owns: [], accepts: [], packs: [] }]);
  }
  if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical', note: '' });
  if (url.startsWith('/api/sync')) return Response.json({ workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] });
  return Response.json([]);
}

/** The window on Overview, over a machine whose driver answers, then the Settings place pressed. */
async function settings() {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (module: string, type: string) => {
    if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
    if (module === 'DAORIS.DRIVER' && type === 'STATE') {
      return { drivable: ['engine'], holds: [], trees: [], running: [], notify: false, strikes: 3, forgiven: {} };
    }
    return {};
  });
  // Get started would open by itself on a machine this bare; this is about the list's doors.
  window.localStorage.setItem(AT_START, 'off');
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={null}>
          <App />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
  // The panel's toggle is drawn once the shell has answered, and the machine's domains are offered with it.
  await screen.findByRole('button', { name: /show or hide the panel/ });
  await userEvent.click(within(bar()).getByRole('button', { name: 'Settings' }));
}

/** Settings' list open beside the domain, holding the machine's domains. */
async function listed() {
  await within(await screen.findByRole('navigation', { name: 'Settings domains' })).findByRole('button', { name: 'Driver' });
}

const TOGGLE = 'show or hide the settings list (Ctrl+B)';
const EDGE = 'settings list width';
const bar = () => screen.getByRole('navigation', { name: 'Views' });

const widen = (width: number) => act(() => {
  Object.defineProperty(window, 'innerWidth', { configurable: true, value: width });
  window.dispatchEvent(new Event('resize'));
});

/** A menu opens from the keyboard in jsdom, the path D41 §6 requires anyway (see `AppMenu.test`). */
async function viewMenu() {
  const user = userEvent.setup();
  screen.getByRole('button', { name: 'View' }).focus();
  await user.keyboard('{Enter}');
  await screen.findByRole('menuitem', { name: /^Commands/ });
  return user;
}

describe("Settings' list, on the window", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
    widen(1024);
  });

  it('names the strip\'s toggle and the View menu\'s item for Settings\' list, ticked while it is shown', async () => {
    await settings();
    await listed();

    expect(screen.getByRole('separator', { name: EDGE })).toHaveAttribute('aria-valuenow', '176');
    await waitFor(() => expect(screen.getByRole('button', { name: TOGGLE })).toHaveAttribute('aria-pressed', 'true'));
    await viewMenu();
    expect(screen.getByRole('menuitem', { name: /^Settings list/ })).toBeInTheDocument();
  });

  it('closes on a press of the Settings place, on Ctrl+B and from the View menu, as Settings\' own', async () => {
    await settings();
    await listed();

    await userEvent.click(within(bar()).getByRole('button', { name: 'Settings' }));
    await waitFor(() => expect(screen.queryByRole('separator', { name: EDGE })).toBeNull());
    expect(window.localStorage.getItem('daoris.list.settings.closed')).toBe('1');
    // Sessions' list is its own, and nothing of it moved.
    expect(window.localStorage.getItem('daoris.railClosed')).toBeNull();

    fireEvent.keyDown(window, { key: 'b', ctrlKey: true });
    expect(await screen.findByRole('separator', { name: EDGE })).toBeInTheDocument();

    const user = await viewMenu();
    await user.click(screen.getByRole('menuitem', { name: /^Settings list/ }));
    await waitFor(() => expect(screen.queryByRole('separator', { name: EDGE })).toBeNull());
  });

  /**
   * At 900 px with the side bar open, the domain would fall below its floor beside the list, so the window
   * draws its strip; the strip's open lays it over the domain, and a domain chosen there closes it.
   */
  it('lays a strip the window drew over the domain, and a domain chosen there closes it', async () => {
    window.localStorage.setItem('daoris.dockClosed', '0');
    widen(900);
    await settings();

    await userEvent.click(await screen.findByRole('button', { name: 'Show the settings list' }));
    const over = await screen.findByRole('region', { name: 'Settings' });
    await userEvent.click(await within(over).findByRole('button', { name: 'Driver' }));

    await waitFor(() => expect(screen.queryByRole('region', { name: 'Settings' })).toBeNull());
    expect(screen.getByRole('heading', { level: 1, name: 'Driver' })).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.settings')).toBe('driver');
  });

  /** D118 §3f: Settings keeps the key its domain was always remembered under, so nothing forgets itself. */
  it('opens on the domain remembered under daoris.settings', async () => {
    window.localStorage.setItem('daoris.settings', 'driver');
    await settings();
    await listed();

    const domains = screen.getByRole('navigation', { name: 'Settings domains' });
    await waitFor(() => expect(within(domains).getByRole('button', { name: 'Driver' })).toHaveAttribute('aria-current', 'page'));
    expect(screen.getByRole('heading', { level: 1, name: 'Driver' })).toBeInTheDocument();
  });
});
