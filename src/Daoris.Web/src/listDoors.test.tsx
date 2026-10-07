import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// D118 §3a, the way the application holds it: the whole window over a mocked shell, so the list's four
// doors — the strip's toggle, the View menu's item, Ctrl+B and a press on the current place — are what a
// person meets on the window, named for the view, and absent on a view with no list.

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

const CHAT = {
  id: 's1a2b3c4', quest: null, repository: 'engine', adapter: 'claude-code', kind: 'chat', state: 'completed',
  created: '2026-09-30T00:00:00Z', updated: '2026-09-30T00:01:00Z', workspace: 'default',
};

function respond(url: string): Response {
  if (url.startsWith('/api/registry')) {
    return Response.json([{ repository: 'engine', workspace: 'default', registered: true, adopted: true, owns: [], accepts: [], packs: [] }]);
  }
  if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical', note: '' });
  if (url.startsWith('/api/sessions')) return Response.json(url.includes('daoris%3Ahelp') ? [] : [CHAT]);
  if (url.startsWith('/api/sync')) return Response.json({ workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] });
  return Response.json([]);
}

/** The window on a view, as a relaunch lands on it, over a machine whose driver answers. */
function start(view: 'sessions' | 'overview') {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (module: string, type: string) => {
    if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
    if (module === 'DAORIS.DRIVER' && type === 'STATE') {
      return { drivable: ['engine'], holds: [], trees: [], running: [], notify: false, strikes: 3, forgiven: {} };
    }
    return {};
  });
  // Get started would open over the view on a machine this bare; this is about the list's doors.
  window.localStorage.setItem(AT_START, 'off');
  if (view === 'sessions') window.localStorage.setItem('daoris.view', 'sessions');
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={null}>
          <App />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

const TOGGLE = 'show or hide the session list (Ctrl+B)';
const bar = () => screen.getByRole('navigation', { name: 'Views' });

/** A menu opens from the keyboard in jsdom, the path D41 §6 requires anyway (see `AppMenu.test`). */
async function viewMenu() {
  const user = userEvent.setup();
  screen.getByRole('button', { name: 'View' }).focus();
  await user.keyboard('{Enter}');
  await screen.findByRole('menuitem', { name: /^Commands/ });
  return user;
}

describe("the list's doors, on the window", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it('names the toggle and the View menu\'s item for Sessions\' list, and ticks them while it is shown', async () => {
    start('sessions');
    const toggle = await screen.findByRole('button', { name: TOGGLE });
    await waitFor(() => expect(toggle).toHaveAttribute('aria-pressed', 'true'));

    await viewMenu();
    // A toggle, said as one (UXFIX1): a screen reader hears that the list is shown, as the tick draws it.
    const item = screen.getByRole('menuitemcheckbox', { name: /^Session list/ });
    expect(item).toHaveAttribute('aria-checked', 'true');
    expect(item.querySelector('svg')).not.toBeNull();
  });

  /** Audit F1: VS Code's activity bar toggles its side bar on a press of the current place. */
  it('toggles the list on a press of the current place, and gives it back on the next', async () => {
    start('sessions');
    await screen.findByRole('separator', { name: 'session list width' });

    await userEvent.click(within(bar()).getByRole('button', { name: 'Sessions' }));
    await waitFor(() => expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull());
    expect(screen.getByRole('button', { name: TOGGLE })).toHaveAttribute('aria-pressed', 'false');
    expect(window.localStorage.getItem('daoris.railClosed')).toBe('1');

    await userEvent.click(within(bar()).getByRole('button', { name: 'Sessions' }));
    expect(await screen.findByRole('separator', { name: 'session list width' })).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.railClosed')).toBeNull();
  });

  /**
   * With the side bar open, 1024 px has no room for the list beside the main area, so the window draws its
   * strip, and a door lays it over. The same door, pressed again, closes it: a press on a door is not a
   * press outside the list, or it would close it and open it again in one go.
   */
  it('lays a strip the window drew over the main area from a door, and the same door closes it', async () => {
    window.localStorage.setItem('daoris.dockClosed', '0');
    start('sessions');
    const toggle = await screen.findByRole('button', { name: TOGGLE });
    await waitFor(() => expect(screen.getByRole('button', { name: 'Show the session list' })).toBeInTheDocument());
    expect(toggle).toHaveAttribute('aria-pressed', 'false');

    await userEvent.click(within(bar()).getByRole('button', { name: 'Sessions' }));
    expect(await screen.findByRole('region', { name: 'Sessions' })).toBeInTheDocument();
    await waitFor(() => expect(toggle).toHaveAttribute('aria-pressed', 'true'));
    await userEvent.click(within(bar()).getByRole('button', { name: 'Sessions' }));
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull());

    await userEvent.click(toggle);
    expect(await screen.findByRole('region', { name: 'Sessions' })).toBeInTheDocument();
    await userEvent.click(toggle);
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Sessions' })).toBeNull());
    // Laying it over was never the person closing it.
    expect(window.localStorage.getItem('daoris.railClosed')).toBeNull();
  });

  it('toggles the list on Ctrl+B, and from the View menu', async () => {
    start('sessions');
    await screen.findByRole('separator', { name: 'session list width' });

    fireEvent.keyDown(window, { key: 'b', ctrlKey: true });
    await waitFor(() => expect(screen.queryByRole('separator', { name: 'session list width' })).toBeNull());

    const user = await viewMenu();
    const item = screen.getByRole('menuitemcheckbox', { name: /^Session list/ });
    expect(item).toHaveAttribute('aria-checked', 'false');
    await user.click(item);
    expect(await screen.findByRole('separator', { name: 'session list width' })).toBeInTheDocument();
  });

  /** D118 §3e: F6 and Shift+F6 walk the window's regions, VS Code's Focus Next Part and Previous Part. */
  it('walks the regions on F6 and back on Shift+F6: the activity bar\'s place, then the list\'s row', async () => {
    start('sessions');
    await screen.findByRole('separator', { name: 'session list width' });
    const rail = await screen.findByRole('navigation', { name: 'Sessions' });
    // The ended chat's row, and its menu.
    await within(rail).findAllByRole('button', { name: /Chat/ });

    fireEvent.keyDown(window, { key: 'F6' });
    expect(within(bar()).getByRole('button', { name: 'Sessions' })).toHaveFocus();
    fireEvent.keyDown(window, { key: 'F6' });
    expect(document.activeElement?.closest('[data-list-row]')).not.toBeNull();
    expect(rail.contains(document.activeElement)).toBe(true);
    fireEvent.keyDown(window, { key: 'F6', shiftKey: true });
    expect(within(bar()).getByRole('button', { name: 'Sessions' })).toHaveFocus();
  });

  it('offers none of the four on a view with no list: absent, never disabled', async () => {
    start('overview');
    await screen.findByRole('button', { name: /show or hide the panel/ });
    expect(screen.queryByRole('button', { name: /Ctrl\+B\)$/ })).toBeNull();

    await userEvent.click(within(bar()).getByRole('button', { name: 'Overview' }));
    fireEvent.keyDown(window, { key: 'b', ctrlKey: true });
    expect(window.localStorage.getItem('daoris.railClosed')).toBeNull();

    await viewMenu();
    // By the role the list's row has where it is present, so this absence is the row's and not a role's (UXFIX1).
    expect(screen.queryByRole('menuitemcheckbox', { name: /^Session list/ })).toBeNull();
    expect(screen.getByRole('menuitemcheckbox', { name: /^Panel/ })).toBeInTheDocument();
  });
});
