import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// BRW7, the way the application holds it: the whole window over a mocked shell, so the browser's door
// and where a link goes are what a person meets on the window.

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

const TICKET = 'https://tickets.example/browse/T-1';

/** A quest whose receiver nobody here registered, so Overview's band lists it and its row opens it. */
const QUEST = {
  id: '7a82cc', from: 'engine', to: 'retired', title: 'Expose a streaming budget', body: 'a per-frame cap.',
  status: 'Open', filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z', links: [TICKET],
};

/** A conversation in `engine`, running — the one the driver may say is driving the browser (BRW8). */
const CHAT = {
  id: 's1a2b3c4', quest: null, repository: 'engine', adapter: 'claude-code', kind: 'chat', state: 'working',
  created: '2026-09-30T00:00:00Z', updated: '2026-09-30T00:01:00Z', workspace: 'default',
};

function respond(url: string): Response {
  if (url.startsWith('/api/registry')) {
    return Response.json([{ repository: 'engine', workspace: 'default', registered: true, adopted: true, owns: [], accepts: [], packs: [] }]);
  }
  if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical', note: '' });
  if (url.startsWith('/api/quests')) return Response.json([QUEST]);
  if (url.startsWith('/api/sessions')) return Response.json(url.includes('daoris%3Ahelp') ? [] : [CHAT]);
  if (url.startsWith('/api/sync')) return Response.json({ workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] });
  return Response.json([]);
}

function machine(links: 'system' | 'daoris' | undefined, drivingBrowser?: string[]) {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (module: string, type: string) => {
    if (module === 'DAORIS.BROWSER') return { favorites: [], extensions: 'offer', browser: 'daoris', edgeFound: true, ...(links ? { links } : {}) };
    if (module === 'DAORIS.WINDOWS') return { opened: true, windows: [] };
    if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
    if (module !== 'DAORIS.DRIVER') return {};
    if (type === 'STATE') {
      return {
        drivable: ['engine'], holds: [], trees: [], running: [CHAT.id], notify: false, strikes: 3, forgiven: {},
        ...(drivingBrowser ? { drivingBrowser } : {}),
      };
    }
    return {};
  });
}

function start() {
  // Get started would open over Overview on a machine this bare; this is about the strip and a link.
  window.localStorage.setItem(AT_START, 'off');
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

const opened = () => invoke.mock.calls.filter(([module, type]) => module === 'DAORIS.WINDOWS' && type === 'OPEN_BROWSER');

/** Open the quest's drawer from Overview's band, and the ticket link in it. */
async function ticketLink() {
  // The band's row and the outstanding row both open it.
  await userEvent.click((await screen.findAllByRole('button', { name: /Expose a streaming budget/ }))[0]!);
  const drawer = await screen.findByRole('dialog', { name: 'Expose a streaming budget' });
  return within(drawer).getByRole('link', { name: TICKET });
}

describe("Daoris's browser, from the window", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  /**
   * The door is on the strip, among its acts, beside the region toggles — not a place on the activity
   * bar, where every item is a view of this window (D66).
   */
  it('has a door on the strip, and none on the activity bar, that opens the browser', async () => {
    machine(undefined);
    start();

    // The strip's right-hand group, where the region toggles are.
    const end = await waitFor(() => {
      const group = document.querySelector<HTMLElement>('[data-strip-space="end"]');
      expect(group).not.toBeNull();
      return group!;
    });
    const door = await within(end).findByRole('button', { name: "open Daoris's browser" });
    expect(within(end).getByRole('button', { name: /show or hide the right side bar/ })).toBeInTheDocument();
    expect(within(screen.getByRole('navigation', { name: 'Views' })).queryByRole('button', { name: /browser/i })).toBeNull();

    await userEvent.click(door);
    expect(opened()).toEqual([['DAORIS.WINDOWS', 'OPEN_BROWSER', {}]]);
  });

  it("opens a quest's link in Daoris's browser where the person chose it, and not in the system's", async () => {
    machine('daoris');
    start();
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.BROWSER', 'STATE', {}));

    const link = await ticketLink();
    await waitFor(() => expect(fireEvent.click(link)).toBe(false));

    expect(opened()).toContainEqual(['DAORIS.WINDOWS', 'OPEN_BROWSER', { payload: { url: TICKET } }]);
  });

  /**
   * BRW8: the strip says whose hands are on the browser beside its door — the session the driver says
   * it handed the browser, by the name the rail gives it — and pressing it opens that session.
   */
  it('names the session driving the browser beside its door, and opens it on a press', async () => {
    machine(undefined, [CHAT.id]);
    start();

    const chip = await screen.findByRole('button', { name: "Daoris's browser is driven by engine · conversation — open the session" });
    expect(chip.closest('[data-strip-space="end"]')).not.toBeNull();
    await userEvent.click(chip);

    const bar = screen.getByRole('navigation', { name: 'Views' });
    await waitFor(() => expect(within(bar).getByRole('button', { name: 'Sessions' })).toHaveAttribute('aria-current', 'page'));
    expect(window.localStorage.getItem('daoris.attending')).toBe(CHAT.id);
  });

  it('says nothing beside the door while no session drives it, and on a shell too old to say', async () => {
    for (const driving of [[], undefined]) {
      machine(undefined, driving);
      start();
      await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STATE', {}));
      await screen.findByRole('button', { name: "open Daoris's browser" });

      expect(screen.queryByText(/driven by/)).toBeNull();
      cleanup();
      invoke.mockReset();
    }
  });

  it("leaves a link to the system's browser by default, and on a shell too old to say", async () => {
    for (const links of ['system', undefined] as const) {
      machine(links);
      start();
      await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.BROWSER', 'STATE', {}));

      const link = await ticketLink();
      expect(fireEvent.click(link)).toBe(true);
      expect(opened()).toEqual([]);

      cleanup();
      invoke.mockReset();
    }
  });
});
